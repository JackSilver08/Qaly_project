<script setup lang="ts">
import { Box, CalendarDays, CheckCircle2, Eye, Pencil, Trash2, UserRound, Plus, FolderKanban } from 'lucide-vue-next'
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

        <div style="display: flex; justify-content: space-between; align-items: center; font-size: 12px; color: #64748b; margin-top: -4px; margin-bottom: 4px;">
          <span style="display: flex; align-items: center; gap: 4px;">
            <CheckCircle2 :size="13" stroke-width="2.5" />
            {{ project.completedTaskCount }}/{{ project.taskCount }} nhiệm vụ
          </span>
          <span v-if="project.overdueTaskCount > 0" style="color: #ef4444; font-weight: 700; background: rgba(239, 68, 68, 0.1); padding: 2px 6px; border-radius: 6px; font-size: 11px;">
            {{ project.overdueTaskCount }} quá hạn
          </span>
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

          <div class="project-grid-card__actions">
            <button type="button" aria-label="Xem dự án" @click.stop="$emit('view', project.id)">
              <Eye :size="15" />
            </button>
            <button v-if="!readOnly" type="button" aria-label="Sửa dự án" @click.stop="$emit('edit', project.id)">
              <Pencil :size="15" />
            </button>
            <button v-if="!readOnly" type="button" aria-label="Xóa dự án" @click.stop="$emit('delete', project.id)">
              <Trash2 :size="15" />
            </button>
          </div>
        </div>
      </div>
    </article>

    <div v-if="projects.length === 0" class="empty-state-container" style="display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 56px 24px; text-align: center; background: rgba(255, 255, 255, 0.45); border: 1px dashed rgba(148, 163, 184, 0.4); border-radius: 16px; margin-top: 12px; box-shadow: inset 0 2px 4px rgba(0, 0, 0, 0.02); grid-column: 1 / -1;">
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
