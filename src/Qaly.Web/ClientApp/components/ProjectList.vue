<script setup lang="ts">
import ProjectListItem from './ProjectListItem.vue'
import type { ProjectCardModel } from './dashboard-models'

defineProps<{
  projects: ProjectCardModel[]
  activeProjectId: string | null
}>()

defineEmits<{
  view: [projectId: string]
  edit: [projectId: string]
  delete: [projectId: string]
}>()
</script>

<template>
  <div class="project-list-shell">
    <ProjectListItem
      v-for="project in projects"
      :key="project.id"
      :project="project"
      :is-active="project.id === activeProjectId"
      @view="$emit('view', $event)"
      @edit="$emit('edit', $event)"
      @delete="$emit('delete', $event)"
    />

    <div v-if="projects.length === 0" class="empty-state">
      Không tìm thấy dự án phù hợp với bộ lọc hiện tại.
    </div>
  </div>
</template>
