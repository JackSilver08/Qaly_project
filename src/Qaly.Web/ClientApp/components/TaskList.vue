<script setup lang="ts">
import TaskItem from './TaskItem.vue'
import type { TaskListItemModel } from './dashboard-models'

defineProps<{
  tasks: TaskListItemModel[]
}>()

defineEmits<{
  view: [projectId: string, taskId: string]
}>()
</script>

<template>
  <div class="task-list-shell">
    <TaskItem
      v-for="task in tasks"
      :key="task.id"
      :task="task"
      @view="(projectId, taskId) => $emit('view', projectId, taskId)"
    />

    <div v-if="tasks.length === 0" class="empty-state">
      Chưa có nhiệm vụ nào được giao cho bạn.
    </div>
  </div>
</template>
