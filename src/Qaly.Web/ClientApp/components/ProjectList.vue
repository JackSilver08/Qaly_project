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

    <div v-if="projects.length === 0" class="project-empty">
      <div class="project-empty__icon">
        <FolderKanban :size="36" stroke-width="1.5" />
      </div>
      <h3>Không tìm thấy dự án nào</h3>
      <p>
        Bắt đầu bằng dự án đầu tiên để theo dõi tiến độ, nhiệm vụ và thành viên ở một
        nơi rõ ràng hơn.
      </p>
      <button v-if="!readOnly" class="primary-button" type="button" @click="$emit('create')">
        <Plus :size="16" />
        Tạo dự án mới
      </button>
    </div>
  </div>
</template>

<style scoped>
.project-list-shell {
  display: grid;
  gap: 12px;
}

.project-empty {
  display: grid;
  justify-items: center;
  gap: 12px;
  padding: 54px 24px;
  border: 1px dashed rgba(148, 163, 184, 0.45);
  border-radius: 18px;
  background: rgba(255, 255, 255, 0.7);
  text-align: center;
}

.project-empty__icon {
  width: 76px;
  height: 76px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  color: #1d4ed8;
  background: rgba(37, 99, 235, 0.08);
}

.project-empty h3 {
  margin: 0;
  color: #0f172a;
  font-size: 16px;
  font-weight: 800;
}

.project-empty p {
  max-width: 340px;
  margin: 0;
  color: #64748b;
  font-size: 13px;
  line-height: 1.55;
}
</style>

<style scoped>
.project-empty {
  border-color: rgba(226, 232, 240, 0.95);
  border-radius: 22px;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.95));
  box-shadow:
    0 14px 32px rgba(15, 23, 42, 0.06),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
}

.project-empty__icon {
  color: #0f766e;
  background: rgba(45, 212, 191, 0.12);
}
</style>
