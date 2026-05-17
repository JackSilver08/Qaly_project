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
import { ref, onMounted, onUnmounted } from 'vue'
import type { DashboardTask } from '../types'
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
              <span>Tasks</span>
              <h2>Board</h2>
            </div>
            <div class="board-actions">
              <div class="search-box">
                <Search :size="16" />
                <input v-model="taskSearchQuery" type="text" placeholder="Tìm task (N: mới)..." />
              </div>
              <button class="primary-button primary-button--compact" type="button" @click="createTaskOpen = !createTaskOpen">
                <Plus :size="16" />
                <span>Task</span>
              </button>
              <button class="import-btn-sm" type="button" @click="showImportModal = true">
                <FileSpreadsheet :size="14" /> Import
              </button>
            </div>
          </div>

          <transition name="expand">
            <form v-if="createTaskOpen" class="task-create-form glass-card" @submit.prevent="createTask">
              <input v-model="newTaskTitle" type="text" placeholder="Task title" required />
              <input v-model="newTaskDescription" type="text" placeholder="Description" />
              <select v-model="newTaskPriority">
                <option v-for="priority in priorities" :key="priority" :value="priority">{{ priority }}</option>
              </select>
              <select v-model="newTaskAssigneeId">
                <option value="">Unassigned</option>
                <option v-for="user in selectedProjectMembers" :key="user.id" :value="user.id">{{ user.fullName }}</option>
              </select>
              <input v-model="newTaskDueDate" type="date" />
              <label class="task-option-toggle">
                <input v-model="newTaskIsPrivate" type="checkbox" />
                <span>Private</span>
              </label>
              <label class="task-option-toggle">
                <input v-model="newTaskContributesToProgress" type="checkbox" />
                <span>Progress</span>
              </label>
              <label v-if="isProjectAdmin" class="task-option-toggle">
                <input v-model="newTaskIsPinned" type="checkbox" />
                <span>Pin</span>
              </label>
              <button class="primary-button primary-button--compact" type="submit">Create</button>
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
                      <span v-if="task.isPrivate" title="Private task">Lock</span>
                      <span v-if="task.isPinned" title="Pinned task">Pin</span>
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
                  <p class="assignee-text">{{ task.isRestricted ? 'Restricted' : (task.assigneeName || 'Unassigned') }} • {{ formatDate(task.dueDate) }}</p>
                  <div class="kanban-card__meta">
                    <span class="meta-item"><MessageSquare :size="12" /> {{ task.commentCount }}</span>
                    <span class="meta-item">▲ {{ task.upvoteCount || 0 }}</span>
                    <span v-if="isTaskOverdue(task)" class="overdue-tag">Overdue</span>
                  </div>
                </article>
                <div v-if="tasksByStatus(status).length === 0" class="empty-column-placeholder">Drop task here</div>
              </VueDraggable>
            </section>
          </div>
        </section>

        <section class="task-detail-panel glass-card">
          <div class="panel-heading">
            <div>
              <span>Task detail</span>
              <h2>{{ selectedTask?.title ?? 'No task selected' }}</h2>
            </div>
            <div v-if="selectedTask" class="task-id-badge">#{{ selectedTask.id.slice(0, 4) }}</div>
          </div>

          <div v-if="selectedTask && !selectedTask.isRestricted" class="comment-list">
            <div class="time-tracking-section">
              <div class="section-header">
                <Clock :size="16" />
                <strong>Activity Log</strong>
              </div>
              
              <div class="timer-display glass-card">
                <div v-if="activeTimer" class="timer-active">
                  <div class="timer-pulse"></div>
                  <span>Ghi giờ: <strong>{{ activeTimer.taskTitle }}</strong></span>
                  <button class="stop-pill" @click="stopTimer(activeTimer.id)">
                    <Square :size="14" fill="currentColor" /> Stop
                  </button>
                </div>
                <div v-else class="timer-idle">
                  <button class="start-pill" @click="startTimer(selectedTask.id)">
                    <Play :size="14" fill="currentColor" /> Start
                  </button>
                  <button class="ghost-pill" @click="showManualForm = !showManualForm">Manual</button>
                </div>
              </div>

              <transition name="fade">
                <div v-if="showManualForm" class="manual-log-form glass-card">
                  <div class="form-row">
                    <input v-model.number="manualMinutes" type="number" placeholder="Min" />
                    <input v-model="manualNote" type="text" placeholder="Ghi chú..." />
                    <button class="primary-button primary-button--compact" @click="submitManualEntry">Log</button>
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
                <strong>Attachments</strong>
                <label class="upload-pill">
                  <input type="file" @change="uploadAttachment" />
                  <span>+ Add</span>
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
              <div class="section-header"><strong>Discussion</strong></div>
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
                  <button v-if="isProjectAdmin || comment.authorId === currentUser?.id" class="bubble-delete" @click="deleteComment(comment.id)">Delete</button>
                </article>
              </div>

              <form class="comment-input-area" @submit.prevent="submitComment">
                <input v-model="newComment" type="text" placeholder="Type a message..." />
                <button class="send-pill" type="submit" :disabled="!newComment.trim()"><Send :size="16" /></button>
              </form>
            </div>
          </div>
          <div v-else class="empty-state-panel">
            <ClipboardList :size="48" />
            <p>{{ selectedTask?.isRestricted ? 'This private task is restricted.' : 'Select a task to see details' }}</p>
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
        />
      </div>

      <div v-if="activeProjectTab === 'wiki'" class="tab-pane reveal">
        <ProjectWikiTab :project-name="selectedProject?.name ?? ''" :is-admin="isProjectAdmin" />
      </div>

      <div v-if="activeProjectTab === 'gantt' && selectedProject" class="tab-pane reveal">
        <ProjectGanttTab :project-id="selectedProject.id" />
      </div>

      <div v-if="activeProjectTab === 'webhooks' && selectedProject" class="tab-pane reveal">
        <WebhooksTab :project-id="selectedProject.id" />
      </div>

      <ImportModal
        v-if="showImportModal && selectedProject"
        :project-id="selectedProject.id"
        :project-name="selectedProject.name"
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
/* Beauty Enhancements */
.kanban-column__list {
  min-height: 300px;
  padding: 4px;
}

.ghost-card {
  opacity: 0.4;
  border: 2px dashed var(--primary) !important;
  transform: scale(0.98);
}

.dragging-card {
  transform: rotate(2deg);
  box-shadow: 0 20px 40px rgba(0,0,0,0.15) !important;
}

.count-badge {
  background: var(--primary-soft);
  color: var(--primary);
  padding: 2px 8px;
  border-radius: 20px;
  font-size: 11px;
  font-weight: 800;
}

.overdue-tag {
  background: #fee2e2;
  color: #ef4444;
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 4px;
}

/* Animations */
.expand-enter-active, .expand-leave-active { transition: all 0.3s ease; }
.expand-enter-from, .expand-leave-to { opacity: 0; transform: translateY(-10px); }

/* Task Details Modernization */
.task-detail-panel {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
  min-height: 600px;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  font-size: 13px;
  color: var(--muted);
}

.timer-display {
  padding: 16px;
  background: linear-gradient(to right, #fff1f2, #fff);
  border: 1px solid #fecaca;
  border-radius: 12px;
  display: flex;
  align-items: center;
}

.timer-pulse {
  width: 8px; height: 8px;
  background: #ef4444;
  border-radius: 50%;
  animation: pulse 1.5s infinite;
  margin-right: 10px;
}

@keyframes pulse {
  0% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(239, 68, 68, 0.7); }
  70% { transform: scale(1); box-shadow: 0 0 0 10px rgba(239, 68, 68, 0); }
  100% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(239, 68, 68, 0); }
}

.start-pill {
  background: #db2777; color: white;
  border: none; padding: 6px 16px; border-radius: 20px;
  font-weight: 700; cursor: pointer; display: flex; align-items: center; gap: 6px;
}

.stop-pill {
  background: #ef4444; color: white;
  border: none; padding: 6px 16px; border-radius: 20px;
  font-weight: 700; cursor: pointer; display: flex; align-items: center; gap: 6px;
}

.ghost-pill {
  background: transparent; border: 1px solid var(--line);
  padding: 6px 12px; border-radius: 20px; font-size: 12px;
  cursor: pointer; margin-left: 8px;
}

.send-pill {
  background: var(--primary); color: white;
  border: none; width: 40px; height: 40px; border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  cursor: pointer; transition: transform 0.2s;
}

.send-pill:hover:not(:disabled) { transform: scale(1.1); }
.send-pill:disabled { opacity: 0.5; }

.comment-bubble {
  background: white;
  padding: 12px 16px;
  border-radius: 12px 12px 12px 0;
  margin-bottom: 12px;
  border: 1px solid var(--line);
  box-shadow: 0 2px 4px rgba(0,0,0,0.02);
}

.bubble-top { display: flex; justify-content: space-between; margin-bottom: 4px; font-size: 12px; }
.bubble-time { color: var(--muted); }
.bubble-delete { border: none; background: transparent; color: #ef4444; font-size: 10px; cursor: pointer; padding: 0; margin-top: 4px; }

.comment-input-area { display: flex; gap: 12px; align-items: center; margin-top: 12px; }
.comment-input-area input { flex: 1; border: 1px solid var(--line); border-radius: 20px; padding: 10px 16px; outline: none; }

.attachment-grid { display: flex; flex-wrap: wrap; gap: 8px; }
.file-chip { 
  display: flex; align-items: center; gap: 8px; background: white; 
  border: 1px solid var(--line); border-radius: 8px; padding: 6px 10px;
}
.file-info { display: flex; flex-direction: column; line-height: 1.2; }
.file-info span { font-size: 10px; color: var(--muted); }
.close-pill { border: none; background: transparent; color: var(--muted); cursor: pointer; }

.truncate { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 120px; }

.empty-state-panel {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  height: 400px; color: var(--muted); opacity: 0.6;
}

/* Keeping the requested beauty */
.project-tabs { width: 100%; justify-content: center; }
.kanban-card { border: 1px solid transparent; }
.kanban-card:hover { border-color: var(--primary-soft); }

.project-home-main { gap: 24px; padding: 24px; }
.tab-link.is-active { box-shadow: 0 4px 15px rgba(31, 128, 255, 0.25); }
.empty-column-placeholder {
  height: 100px; border: 2px dashed var(--line); border-radius: 12px;
  display: flex; align-items: center; justify-content: center;
  color: var(--muted); font-size: 13px; margin: 8px 0;
}

.task-option-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 700;
  color: var(--muted);
  white-space: nowrap;
}

.import-btn-sm {
  display: inline-flex; align-items: center; gap: 5px;
  padding: 5px 12px; border-radius: 8px; font-size: 12px; font-weight: 600;
  background: rgba(99,102,241,.1); color: #818cf8;
  border: 1px solid rgba(99,102,241,.18); cursor: pointer;
  transition: all .2s;
}
.import-btn-sm:hover { background: rgba(99,102,241,.18); }
</style>
