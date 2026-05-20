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
      <div class="project-grid-card__body">
        <div class="project-grid-card__header">
          <div class="project-grid-card__header-left">
            <div :class="`project-grid-card__icon project-grid-card__icon--${project.statusTone}`">
              <component :is="project.statusTone === 'active' ? CalendarDays : (project.statusTone === 'planned' ? Eye : Box)" :size="16" />
            </div>
            <div class="project-grid-card__title-col">
              <strong>{{ project.name }}</strong>
              <span>Hết hạn: {{ project.dueDateLabel }}</span>
            </div>
          </div>
          <span :class="`project-grid-card__badge project-grid-card__badge--${project.statusTone}`">
            {{ project.statusLabel.toUpperCase() }}
          </span>
        </div>

        <div class="project-grid-card__progress-wrap">
          <div class="project-grid-card__progress-header">
            <span>Tiến độ</span>
            <span>{{ project.progressPercentage }}%</span>
          </div>
          <div class="project-grid-card__progress-track" aria-hidden="true">
            <span :class="`project-grid-card__progress-bar--${project.statusTone}`" :style="{ width: `${project.progressPercentage}%` }"></span>
          </div>
        </div>

        <div class="project-grid-card__footer">
          <div class="project-grid-card__team-overlap" aria-label="Thành viên dự án">
            <div v-for="(member, index) in project.memberInitials.slice(0, 3)" :key="`${member}-${index}`" class="team-avatar">
              {{ member }}
            </div>
            <div v-if="project.memberInitials.length > 3" class="team-avatar team-avatar--more">
              +{{ project.memberInitials.length - 3 }}
            </div>
          </div>

          <div class="project-grid-card__tasks-count">
            <CheckCircle2 :size="14" />
            <span>{{ project.completedTaskCount }}/{{ project.taskCount }} Nhiệm vụ</span>
          </div>
        </div>
      </div>
    </article>

    <div v-if="projects.length === 0" class="empty-state">
      Không tìm thấy dự án phù hợp với bộ lọc hiện tại.
    </div>
  </div>
</template>
