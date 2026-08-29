<script setup lang="ts">
import { CalendarDays, UserRound } from 'lucide-vue-next'
import type { TaskListItemModel } from './dashboard-models'

defineProps<{
  task: TaskListItemModel
}>()

defineEmits<{
  view: [projectId: string, taskId: string]
}>()
</script>

<template>
  <article
    class="task-list-item"
    role="button"
    tabindex="0"
    :aria-label="`Mở nhiệm vụ ${task.title}`"
    @click="$emit('view', task.projectId, task.id)"
    @keydown.enter.prevent="$emit('view', task.projectId, task.id)"
    @keydown.space.prevent="$emit('view', task.projectId, task.id)"
  >
    <div class="task-list-item__main">
      <div class="task-list-item__title">
        <strong>{{ task.title }}</strong>
        <span :class="`priority priority--${task.priority.toLowerCase()}`">{{ task.priority }}</span>
      </div>
      <div class="task-list-item__meta">
        <span>{{ task.projectName }}</span>
        <span>
          <CalendarDays :size="14" />
          {{ task.assignedAtLabel }}
        </span>
        <span :class="{ 'project-risk': task.isOverdue }">Deadline {{ task.dueDateLabel }}</span>
      </div>
    </div>

    <div class="task-list-item__reporter">
      <span>{{ task.reporterInitials }}</span>
      <div>
        <small>Người giao</small>
        <strong>{{ task.reporterName }}</strong>
      </div>
    </div>

    <div class="task-list-item__status">
      <UserRound :size="15" />
      <span>{{ task.statusLabel }}</span>
    </div>
  </article>
</template>
