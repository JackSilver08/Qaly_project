<script setup lang="ts">
import { Eye, Pencil, Trash2 } from 'lucide-vue-next'
import type { ProjectCardModel } from './dashboard-models'

defineProps<{
  project: ProjectCardModel
  isActive: boolean
  readOnly?: boolean
}>()

defineEmits<{
  view: [projectId: string]
  edit: [projectId: string]
  delete: [projectId: string]
}>()
</script>

<template>
  <article class="project-list-item" :class="{ 'is-active': isActive }">
    <button class="project-list-item__main" type="button" @click="$emit('view', project.id)">
      <div class="project-list-item__title">
        <strong>{{ project.name }}</strong>
        <span :class="`project-status project-status--${project.statusTone}`">{{ project.statusLabel }}</span>
      </div>
      <p>{{ project.description }}</p>
      <div class="project-list-item__meta">
        <span>{{ project.ownerName }}</span>
        <span>{{ project.completedTaskCount }}/{{ project.taskCount }} task</span>
        <span>{{ project.dueDateLabel }}</span>
        <span v-if="project.overdueTaskCount > 0" class="project-risk">{{ project.overdueTaskCount }} quá hạn</span>
      </div>
    </button>

    <div class="project-list-item__progress" aria-hidden="true">
      <span :style="{ width: `${project.progressPercentage}%` }"></span>
    </div>

    <div class="project-list-item__team" aria-label="Thành viên dự án">
      <span v-for="member in project.memberInitials" :key="member">{{ member }}</span>
    </div>

    <div v-if="!readOnly" class="project-list-item__actions">
      <button type="button" aria-label="Xem dự án" @click="$emit('view', project.id)">
        <Eye :size="17" />
      </button>
      <button type="button" aria-label="Sửa dự án" @click="$emit('edit', project.id)">
        <Pencil :size="17" />
      </button>
      <button type="button" aria-label="Xóa dự án" @click="$emit('delete', project.id)">
        <Trash2 :size="17" />
      </button>
    </div>

    <div v-else class="project-list-item__actions">
      <button type="button" aria-label="Xem dự án" @click="$emit('view', project.id)">
        <Eye :size="17" />
      </button>
    </div>
  </article>
</template>
