<script setup lang="ts">
import { CalendarDays, CheckCircle2, Eye, Pencil, Trash2, UserRound } from 'lucide-vue-next'
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
}>()
</script>

<template>
  <div class="project-grid-shell">
    <article
      v-for="project in projects"
      :key="project.id"
      class="project-grid-card"
      :class="{ 'is-active': project.id === activeProjectId }"
      role="button"
      tabindex="0"
      @click="$emit('view', project.id)"
      @keydown.enter="$emit('view', project.id)"
      @keydown.space.prevent="$emit('view', project.id)"
    >
      <span :class="`project-grid-card__status-tab project-grid-card__status-tab--${project.statusTone}`">
        {{ project.statusLabel }}
      </span>

      <div class="project-grid-card__body">
        <div class="project-grid-card__title-row">
          <strong>{{ project.name }}</strong>
          <span>{{ project.progressPercentage }}%</span>
        </div>

        <p>{{ project.description }}</p>

        <div class="project-grid-card__meta">
          <span>
            <UserRound :size="13" />
            {{ project.ownerName }}
          </span>
          <span>
            <CheckCircle2 :size="13" />
            {{ project.completedTaskCount }}/{{ project.taskCount }} task
          </span>
          <span>
            <CalendarDays :size="13" />
            {{ project.dueDateLabel }}
          </span>
        </div>

        <div class="project-grid-card__progress" aria-hidden="true">
          <span :style="{ width: `${project.progressPercentage}%` }"></span>
        </div>

        <div class="project-grid-card__footer">
          <div class="project-grid-card__team" aria-label="Thành viên dự án">
            <span v-for="(member, index) in project.memberInitials" :key="`${member}-${index}`">{{ member }}</span>
          </div>

          <div class="project-grid-card__actions">
            <button type="button" aria-label="Xem dự án" @click.stop="$emit('view', project.id)">
              <Eye :size="16" />
            </button>
            <button
              v-if="!readOnly"
              type="button"
              aria-label="Sửa dự án"
              @click.stop="$emit('edit', project.id)"
            >
              <Pencil :size="16" />
            </button>
            <button
              v-if="!readOnly"
              type="button"
              aria-label="Xóa dự án"
              @click.stop="$emit('delete', project.id)"
            >
              <Trash2 :size="16" />
            </button>
          </div>
        </div>
      </div>
    </article>

    <div v-if="projects.length === 0" class="empty-state">
      Không tìm thấy dự án phù hợp với bộ lọc hiện tại.
    </div>
  </div>
</template>
