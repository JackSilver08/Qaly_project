<script setup lang="ts">
import { MessageSquare, MoreHorizontal, Plus, Send, Search, Clock, Play, Square, Calendar } from 'lucide-vue-next'
import ProjectDetailHeader from '../components/ProjectDetailHeader.vue'
import ProjectMembersTab from '../components/ProjectMembersTab.vue'
import ProjectStatsTab from '../components/ProjectStatsTab.vue'
import ProjectWikiTab from '../components/ProjectWikiTab.vue'
import { useDashboardContext } from '../composables/dashboard-context'
import { ref } from 'vue'

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
} = useDashboardContext()

const quickEditTitle = ref('')
const manualMinutes = ref<number>(0)
const manualNote = ref('')
const showManualForm = ref(false)

function startQuickEdit(task: any) {
  taskBeingQuickEditedId.value = task.id
  quickEditTitle.value = task.title
}

async function saveQuickEdit() {
  if (!taskBeingQuickEditedId.value || !selectedTask.value) return
  let saved = false
  
  try {
    const taskId = taskBeingQuickEditedId.value
    saved = await quickEditTaskTitle(taskId, quickEditTitle.value)
  } catch (e) {
    console.error(e)
  } finally {
    if (saved) taskBeingQuickEditedId.value = null
  }
}

async function submitManualEntry() {
  if (!selectedTask.value || manualMinutes.value <= 0) return

  try {
    const saved = await addManualTimeEntry(selectedTask.value.id, manualMinutes.value, manualNote.value)
    if (!saved) return

    manualMinutes.value = 0
    manualNote.value = ''
    showManualForm.value = false
  } catch (e) {
    console.error(e)
  }
}
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
                <input v-model="taskSearchQuery" type="text" placeholder="Tìm task..." />
              </div>
              <button class="primary-button primary-button--compact" type="button" @click="createTaskOpen = !createTaskOpen">
                <Plus :size="16" />
                <span>Task</span>
              </button>
            </div>
          </div>

          <form v-if="createTaskOpen" class="task-create-form" @submit.prevent="createTask">
            <input v-model="newTaskTitle" type="text" placeholder="Task title" />
            <input v-model="newTaskDescription" type="text" placeholder="Description" />
            <select v-model="newTaskPriority" aria-label="Priority">
              <option v-for="priority in priorities" :key="priority" :value="priority">{{ priority }}</option>
            </select>
            <select v-model="newTaskAssigneeId" aria-label="Assignee">
              <option value="">Unassigned</option>
              <option v-for="user in users" :key="user.id" :value="user.id">{{ user.fullName }}</option>
            </select>
            <input v-model="newTaskDueDate" type="date" />
            <button class="primary-button primary-button--compact" type="submit" :disabled="!newTaskTitle.trim()">Create</button>
          </form>

          <div class="kanban-board">
            <section v-for="status in statusColumns" :key="status" class="kanban-column">
              <div class="kanban-column__header">
                <strong>{{ displayStatus(status) }}</strong>
                <span>{{ tasksByStatus(status).length }}</span>
              </div>

              <article
                v-for="task in tasksByStatus(status)"
                :key="task.id"
                class="kanban-card"
                :class="{ 'is-selected': selectedTask?.id === task.id }"
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
                  <strong v-else @dblclick.stop="startQuickEdit(task)">{{ task.title }}</strong>
                  
                  <div class="task-card-actions">
                    <span :class="`priority priority--${task.priority.toLowerCase()}`">{{ task.priority }}</span>

                    <div v-if="isProjectAdmin" class="task-menu-dropdown">
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
                <p>{{ task.assigneeName || 'Unassigned' }} - {{ formatDate(task.dueDate) }}</p>
                <div class="kanban-card__meta">
                  <span>{{ task.commentCount }} comments</span>
                  <span v-if="task.isPrivate">Private</span>
                  <span v-if="isTaskOverdue(task)" class="project-risk">Overdue</span>
                </div>
                <div class="kanban-card__actions">
                  <button
                    v-for="nextStatus in nextStatuses(task.status)"
                    :key="nextStatus"
                    type="button"
                    @click.stop="moveTask(task, nextStatus)"
                  >
                    {{ displayStatus(nextStatus) }}
                  </button>
                </div>
              </article>

              <div v-if="tasksByStatus(status).length === 0" class="empty-state">No tasks</div>
            </section>
          </div>
        </section>

        <section class="task-detail-panel glass-card" style="margin-top: 24px;">
          <div class="panel-heading">
            <div>
              <span>Task detail</span>
              <h2>{{ selectedTask?.title ?? 'No task selected' }}</h2>
            </div>
            <MessageSquare :size="18" />
          </div>

          <div v-if="selectedTask" class="comment-list">
            <!-- Time Tracking Section -->
            <div class="time-tracking-panel">
              <div class="panel-subheading">
                <Clock :size="16" />
                <strong>Time Tracking</strong>
              </div>
              
              <div class="timer-controls">
                <div v-if="activeTimer" class="active-timer">
                  <span>Đang tính giờ cho: <strong>{{ activeTimer.taskTitle }}</strong></span>
                  <button class="stop-button" @click="stopTimer(activeTimer.id)">
                    <Square :size="14" /> Stop
                  </button>
                </div>
                <div v-else class="timer-actions">
                  <button class="start-button" @click="startTimer(selectedTask.id)">
                    <Play :size="14" /> Start Timer
                  </button>
                  <button class="text-button" @click="showManualForm = !showManualForm">
                    {{ showManualForm ? 'Cancel' : 'Manual Entry' }}
                  </button>
                </div>
              </div>

              <!-- Manual Entry Form -->
              <div v-if="showManualForm" class="manual-entry-form">
                <div class="form-row">
                  <input v-model.number="manualMinutes" type="number" placeholder="Phút" />
                  <input v-model="manualNote" type="text" placeholder="Ghi chú..." />
                  <button class="primary-button primary-button--compact" @click="submitManualEntry">Log</button>
                </div>
              </div>

              <div v-if="timeEntries.length > 0" class="time-logs">
                <div v-for="entry in timeEntries.slice(0, 5)" :key="entry.id" class="time-log-row">
                  <span>{{ entry.userName }}</span>
                  <span v-if="entry.manualMinutes"><strong>{{ entry.manualMinutes }}m</strong> (Manual)</span>
                  <span v-else>{{ entry.totalMinutes }} phút</span>
                  <span>{{ formatDate(entry.startedAt) }}</span>
                </div>
              </div>
            </div>

            <div class="attachment-panel">
              <div class="attachment-panel__header">
                <strong>Attachments</strong>
                <label class="attachment-upload">
                  <input type="file" @change="uploadAttachment" />
                  <span>Upload</span>
                </label>
              </div>
              <article v-for="attachment in attachments" :key="attachment.id" class="attachment-row">
                <div>
                  <strong>{{ attachment.fileName }}</strong>
                  <span>{{ formatFileSize(attachment.fileSize) }} - {{ attachment.uploadedByName }}</span>
                </div>
                <button type="button" @click="deleteAttachment(attachment)">Delete</button>
              </article>
              <div v-if="attachments.length === 0" class="empty-state">No attachments.</div>
            </div>

            <article v-for="comment in comments" :key="comment.id" class="comment-row">
              <div class="comment-row__top">
                <strong>{{ comment.authorName }}</strong>
                <button
                  v-if="isProjectAdmin || comment.authorId === currentUser?.id"
                  class="text-button"
                  style="color: var(--peach-500); padding: 0 4px; height: auto;"
                  type="button"
                  @click="deleteComment(comment.id)"
                >
                  Xóa
                </button>
              </div>
              <p>{{ comment.content }}</p>
              <span>{{ formatTime(comment.createdAt) }}</span>
            </article>
            <div v-if="comments.length === 0" class="empty-state">No comments yet.</div>

            <form class="comment-form" @submit.prevent="submitComment">
              <input v-model="newComment" type="text" placeholder="Add a comment..." />
              <button class="primary-button primary-button--compact" type="submit" :disabled="!newComment.trim()">
                <Send :size="15" />
              </button>
            </form>
          </div>

          <div v-else class="empty-state">Select a task from the board.</div>
        </section>
      </div>

      <div v-if="activeProjectTab === 'members'" class="tab-pane reveal">
        <ProjectMembersTab
          :members="selectedProjectMembers"
          :users="users"
          :is-admin="true"
          @add="addMember"
          @remove="removeMember"
          @update-role="updateMemberRole"
        />
      </div>

      <div v-if="activeProjectTab === 'wiki'" class="tab-pane reveal">
        <ProjectWikiTab
          :project-name="selectedProject?.name ?? ''"
          :is-admin="true"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
/* Project Details Page Premium Styles */
.project-home-main {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-3);
}

.project-tabs {
  display: flex;
  gap: var(--space-1);
  padding: var(--space-1) var(--space-2);
  margin-bottom: var(--space-3);
  background: var(--glass-strong);
  border-radius: var(--radius-pill);
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.05);
  backdrop-filter: blur(20px);
  width: fit-content;
}

.tab-link {
  padding: 10px 24px;
  border: none;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--muted);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
  overflow: hidden;
}

.tab-link:hover {
  color: var(--primary);
  background: rgba(31, 128, 255, 0.05);
}

.tab-link.is-active {
  color: white;
  background: linear-gradient(135deg, var(--primary), var(--blue-600));
  box-shadow: 0 6px 15px rgba(31, 128, 255, 0.3);
  transform: translateY(-1px);
}

/* Kanban Board Styling */
.task-board-shell {
  padding: var(--space-3);
  background: var(--glass);
  border-radius: var(--radius-shell);
}

.panel-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--space-3);
  padding-bottom: var(--space-2);
  border-bottom: 1px solid var(--glass-border);
}

.panel-heading span {
  font-size: 12px;
  text-transform: uppercase;
  color: var(--primary);
  font-weight: 800;
  letter-spacing: 0.5px;
}

.panel-heading h2 {
  font-size: 22px;
  color: var(--text);
}

.board-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  background: white;
  padding: 6px 12px;
  border-radius: 10px;
  border: 1px solid var(--line);
}

.search-box input {
  border: none;
  outline: none;
  font-size: 13px;
  width: 150px;
}

.task-create-form {
  display: grid;
  grid-template-columns: 2fr 3fr 1fr 1.5fr 1.5fr auto;
  gap: var(--space-2);
  margin-bottom: var(--space-4);
  padding: var(--space-2);
  background: rgba(255, 255, 255, 0.5);
  border-radius: var(--radius-card);
  border: 1px solid var(--glass-border);
}

@media (max-width: 1024px) {
  .task-create-form {
    grid-template-columns: 1fr;
  }
}

.task-create-form input,
.task-create-form select {
  padding: 10px 14px;
  border-radius: 10px;
  border: 1px solid var(--line);
  background: white;
  color: var(--text);
  font-size: 13px;
  outline: none;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.task-create-form input:focus,
.task-create-form select:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px var(--primary-soft);
}

.kanban-board {
  display: flex;
  gap: var(--space-3);
  overflow-x: auto;
  padding-bottom: var(--space-2);
  min-height: 400px;
}

.kanban-column {
  flex: 0 0 320px;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  background: rgba(248, 250, 252, 0.6);
  padding: var(--space-2);
  border-radius: var(--radius-panel);
  border: 1px solid var(--glass-border);
}

.kanban-column__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 4px;
  margin-bottom: 8px;
}

.kanban-column__header strong {
  font-size: 14px;
  color: var(--text);
}

.kanban-column__header span {
  background: var(--blue-100);
  color: var(--blue-700);
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 700;
}

.kanban-card {
  background: white;
  border-radius: var(--radius-card);
  padding: 16px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.03);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all 0.2s ease;
  position: relative;
}

.kanban-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 24px rgba(31, 128, 255, 0.1);
  border-color: var(--primary-soft);
}

.kanban-card.is-selected {
  border-color: var(--primary);
  box-shadow: 0 0 0 2px var(--primary-soft);
}

.kanban-card__top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.kanban-card__top strong {
  font-size: 14px;
  color: var(--text);
  line-height: 1.4;
  flex: 1;
  padding-right: 8px;
}

.quick-edit-input {
  width: 100%;
  padding: 4px 8px;
  border-radius: 4px;
  border: 1px solid var(--primary);
  font-size: 14px;
  outline: none;
}

.priority {
  font-size: 11px;
  padding: 3px 8px;
  border-radius: 6px;
  font-weight: 700;
  text-transform: uppercase;
  display: inline-block;
  white-space: nowrap;
}

.priority--high { background: var(--peach-100); color: var(--peach-500); }
.priority--medium { background: #fef08a; color: #ca8a04; }
.priority--low { background: var(--mint-100); color: var(--mint-500); }

.kanban-card p {
  font-size: 12px;
  margin-bottom: 12px;
  color: var(--muted);
}

.kanban-card__meta {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
  font-size: 12px;
  color: var(--muted);
}

.kanban-card__actions {
  display: flex;
  gap: 6px;
  border-top: 1px solid var(--line);
  padding-top: 12px;
}

.kanban-card__actions button {
  flex: 1;
  background: var(--surface-warm);
  border: 1px solid var(--line);
  border-radius: 6px;
  padding: 4px 0;
  font-size: 11px;
  color: var(--text);
  transition: all 0.2s;
  cursor: pointer;
}

.kanban-card__actions button:hover {
  background: var(--primary-soft);
  color: var(--primary);
  border-color: var(--primary-soft);
}

.task-menu-dropdown {
  position: relative;
}

.dropdown-content {
  position: absolute;
  top: 100%;
  right: 0;
  width: 120px;
  display: flex;
  flex-direction: column;
  padding: 8px;
  z-index: 10;
  gap: 4px;
}

.dropdown-content button {
  text-align: left;
  padding: 6px 10px;
  background: transparent;
  border: none;
  border-radius: 6px;
  font-size: 13px;
  color: var(--text);
  transition: background 0.2s;
  cursor: pointer;
}

.dropdown-content button:hover {
  background: var(--surface-warm);
}

/* Time Tracking Styles */
.time-tracking-panel {
  background: #fdf2f8;
  border-radius: 12px;
  padding: 16px;
  margin-bottom: 16px;
  border: 1px solid #fbcfe8;
}

.panel-subheading {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  color: #db2777;
}

.timer-controls {
  display: flex;
  justify-content: center;
  margin-bottom: 12px;
}

.start-button {
  background: #db2777;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 20px;
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-weight: 600;
}

.active-timer {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.stop-button {
  background: #ef4444;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 20px;
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.timer-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.manual-entry-form {
  background: white;
  padding: 12px;
  border-radius: 8px;
  margin-bottom: 12px;
  border: 1px solid #fbcfe8;
}

.form-row {
  display: flex;
  gap: 8px;
}

.form-row input[type="number"] {
  width: 60px;
}

.form-row input {
  padding: 6px 10px;
  border-radius: 6px;
  border: 1px solid var(--line);
  font-size: 12px;
}

.time-logs {
  border-top: 1px solid #fbcfe8;
  padding-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.time-log-row {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
  color: #831843;
}

/* Detail Panel */
.task-detail-panel {
  padding: var(--space-4);
  margin-bottom: var(--space-4);
}

.comment-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.attachment-panel {
  background: var(--surface-milk);
  border-radius: var(--radius-card);
  padding: var(--space-3);
  border: 1px dashed var(--glass-border);
}

.attachment-panel__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.attachment-upload input {
  display: none;
}

.attachment-upload span {
  background: var(--primary-soft);
  color: var(--primary);
  padding: 6px 12px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}

.attachment-upload span:hover {
  background: var(--blue-100);
}

.attachment-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 16px;
  background: white;
  border-radius: 10px;
  margin-bottom: 8px;
  box-shadow: 0 2px 6px rgba(0,0,0,0.02);
  border: 1px solid var(--line);
}

.attachment-row button {
  color: var(--peach-500);
  background: transparent;
  border: none;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 6px;
  transition: background 0.2s;
}

.attachment-row button:hover {
  background: var(--peach-100);
}

.comment-row {
  background: rgba(255,255,255,0.7);
  padding: 16px 20px;
  border-radius: var(--radius-card);
  border: 1px solid var(--glass-border);
  box-shadow: 0 4px 12px rgba(0,0,0,0.02);
}

.comment-row__top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.comment-row__top strong {
  color: var(--primary);
  font-size: 14px;
}

.comment-row p {
  color: var(--text);
  margin-bottom: 10px;
  font-size: 14px;
  line-height: 1.5;
}

.comment-row span {
  font-size: 11px;
  color: var(--muted);
}

.comment-form {
  display: flex;
  gap: 12px;
  margin-top: var(--space-2);
}

.comment-form input {
  flex: 1;
  padding: 14px 20px;
  border-radius: 24px;
  border: 1px solid var(--glass-border);
  background: white;
  outline: none;
  box-shadow: inset 0 2px 4px rgba(0,0,0,0.02);
  font-size: 14px;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.comment-form input:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px var(--primary-soft);
}

/* Utilities */
.primary-button {
  background: linear-gradient(135deg, var(--primary), var(--blue-600));
  color: white;
  border: none;
  border-radius: 12px;
  padding: 10px 20px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 8px;
  transition: all 0.2s;
  box-shadow: 0 4px 12px rgba(31, 128, 255, 0.2);
  cursor: pointer;
}

.primary-button:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(31, 128, 255, 0.3);
}

.primary-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.primary-button--compact {
  padding: 8px 16px;
  font-size: 13px;
  border-radius: 10px;
}

.icon-button {
  background: white;
  border: 1px solid var(--glass-border);
  border-radius: 8px;
  width: 28px;
  height: 28px;
  display: flex;
  justify-content: center;
  align-items: center;
  color: var(--muted);
  transition: all 0.2s;
  cursor: pointer;
}

.icon-button:hover {
  color: var(--primary);
  border-color: var(--primary);
  background: var(--primary-soft);
}

.empty-state {
  text-align: center;
  padding: 40px;
  color: var(--muted);
  font-size: 14px;
  background: rgba(255,255,255,0.4);
  border-radius: var(--radius-card);
  border: 1px dashed var(--glass-border);
}

.text-button {
  background: transparent;
  border: none;
  cursor: pointer;
  font-weight: 600;
  font-size: 13px;
}

.text-button:hover {
  text-decoration: underline;
}

/* Animations */
.reveal {
  animation: fade-in-up 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

@keyframes fade-in-up {
  0% { opacity: 0; transform: translateY(10px); }
  100% { opacity: 1; transform: translateY(0); }
}
</style>
