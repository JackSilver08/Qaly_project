<script setup lang="ts">
import { MessageSquare, MoreHorizontal, Plus, Send } from 'lucide-vue-next'
import ProjectDetailHeader from '../components/ProjectDetailHeader.vue'
import ProjectMembersTab from '../components/ProjectMembersTab.vue'
import ProjectStatsTab from '../components/ProjectStatsTab.vue'
import ProjectWikiTab from '../components/ProjectWikiTab.vue'
import { useDashboardContext } from '../composables/dashboard-context'

const {
  activeProjectTab,
  activeTaskMenu,
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
} = useDashboardContext()
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
            <button class="primary-button primary-button--compact" type="button" @click="createTaskOpen = !createTaskOpen">
              <Plus :size="16" />
              <span>Task</span>
            </button>
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
                  <strong>{{ task.title }}</strong>
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
          :is-admin="isProjectAdmin"
          @add="addMember"
          @remove="removeMember"
          @update-role="updateMemberRole"
        />
      </div>

      <div v-if="activeProjectTab === 'wiki'" class="tab-pane reveal">
        <ProjectWikiTab
          :project-name="selectedProject?.name ?? ''"
          :is-admin="isProjectAdmin"
        />
      </div>
    </div>
  </div>
</template>
