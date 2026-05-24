<script setup lang="ts">
import { FolderKanban, Plus } from 'lucide-vue-next'
import ProjectListItem from './ProjectListItem.vue'
import type { ProjectCardModel } from './dashboard-models'

defineProps<{
  projects: ProjectCardModel[]
  activeProjectId: string | null
  readOnly?: boolean
}>()

defineEmits<{
  view: [projectId: string]
  edit: [projectId: string]
  delete: [projectId: string]
  create: []
}>()
</script>

<template>
  <div class="project-list-shell">
    <ProjectListItem
      v-for="project in projects"
      :key="project.id"
      :project="project"
      :is-active="project.id === activeProjectId"
      :read-only="readOnly"
      @view="$emit('view', $event)"
      @edit="$emit('edit', $event)"
      @delete="$emit('delete', $event)"
    />

    <div v-if="projects.length === 0" class="empty-state-container" style="display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 56px 24px; text-align: center; background: rgba(255, 255, 255, 0.45); border: 1px dashed rgba(148, 163, 184, 0.4); border-radius: 16px; margin-top: 12px; box-shadow: inset 0 2px 4px rgba(0, 0, 0, 0.02);">
      <div style="background: rgba(37, 99, 235, 0.08); width: 76px; height: 76px; border-radius: 50%; display: grid; place-items: center; margin-bottom: 16px; color: var(--primary);">
        <FolderKanban :size="36" stroke-width="1.5" />
      </div>
      <h3 style="font-size: 16px; font-weight: 800; color: var(--text); margin: 0 0 6px 0;">Không tìm thấy dự án nào</h3>
      <p style="font-size: 13px; color: var(--muted); max-width: 320px; margin: 0 0 20px 0; line-height: 1.5;">Bắt đầu quản lý công việc và cộng tác với đội ngũ của bạn bằng cách tạo dự án mới hoặc thay đổi bộ lọc.</p>
      <button v-if="!readOnly" class="primary-button" type="button" @click="$emit('create')">
        <Plus :size="16" />
        Tạo dự án mới
      </button>
    </div>
  </div>
</template>
