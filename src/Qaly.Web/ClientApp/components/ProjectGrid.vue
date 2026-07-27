<script setup lang="ts">
import { CheckCircle2, Eye, FolderKanban, Pencil, Plus, Trash2 } from 'lucide-vue-next'
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
      <div class="project-grid-card__accent" :class="`is-${project.statusTone}`" />
      <span :class="`project-grid-card__badge project-grid-card__badge--${project.statusTone}`">
        {{ project.statusLabel.toLowerCase() }}
      </span>

      <div class="project-grid-card__body">
        <div class="project-grid-card__title-col">
          <strong>{{ project.name }}</strong>
          <span>{{ project.description }}</span>
        </div>

        <div class="project-grid-card__metrics">
          <div class="project-grid-card__progress-wrap">
            <div class="project-grid-card__progress-header">
              <span>Tiến độ</span>
              <span>{{ project.progressPercentage }}%</span>
            </div>
            <div class="project-grid-card__progress-track" aria-hidden="true">
              <span :style="{ width: `${project.progressPercentage}%` }" />
            </div>
          </div>

          <div class="project-grid-card__tasks-row">
            <span>
              <CheckCircle2 :size="16" stroke-width="2.6" />
              {{ project.completedTaskCount }}/{{ project.taskCount }} nhiệm vụ
            </span>
            <span v-if="project.overdueTaskCount > 0" class="project-grid-card__risk">
              {{ project.overdueTaskCount }} quá hạn
            </span>
          </div>
        </div>

        <div class="project-grid-card__footer">
          <div class="project-grid-card__team-overlap" aria-label="Thành viên dự án">
            <div
              v-for="(member, index) in project.memberInitials.slice(0, 3)"
              :key="`${member}-${index}`"
              class="team-avatar"
            >
              {{ member }}
            </div>
            <div v-if="project.memberInitials.length > 3" class="team-avatar team-avatar--more">
              +{{ project.memberInitials.length - 3 }}
            </div>
          </div>

          <div class="project-grid-card__actions">
            <button type="button" aria-label="Xem dự án" @click.stop="$emit('view', project.id)">
              <Eye :size="18" />
            </button>
            <button
              v-if="!readOnly"
              type="button"
              aria-label="Sửa dự án"
              @click.stop="$emit('edit', project.id)"
            >
              <Pencil :size="18" />
            </button>
            <button
              v-if="!readOnly"
              type="button"
              aria-label="Xóa dự án"
              @click.stop="$emit('delete', project.id)"
            >
              <Trash2 :size="18" />
            </button>
          </div>
        </div>
      </div>
    </article>

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
.project-grid-shell {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 14px;
}

.project-grid-card {
  position: relative;
  overflow: visible;
  display: grid;
  gap: 14px;
  padding: 24px 18px 18px;
  border: 1px solid rgba(226, 232, 240, 0.96);
  border-radius: 22px;
  background:
    radial-gradient(circle at top right, rgba(31, 128, 255, 0.04), transparent 28%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(248, 250, 252, 0.96));
  box-shadow:
    0 12px 28px rgba(15, 23, 42, 0.05),
    inset 0 1px 0 rgba(255, 255, 255, 0.88);
  transition:
    transform 180ms ease,
    box-shadow 180ms ease,
    border-color 180ms ease;
}

.project-grid-card:hover,
.project-grid-card:focus-visible,
.project-grid-card.is-active {
  transform: translateY(-3px);
  border-color: rgba(191, 219, 254, 0.98);
  box-shadow:
    0 18px 38px rgba(15, 23, 42, 0.08),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
  outline: none;
}

.project-grid-card__accent {
  position: absolute;
  inset: 0 auto auto 0;
  width: 100%;
  height: 4px;
}

.project-grid-card__accent.is-success {
  background: linear-gradient(90deg, #16a34a, #4ade80);
}

.project-grid-card__accent.is-warning {
  background: linear-gradient(90deg, #d97706, #fbbf24);
}

.project-grid-card__accent.is-danger {
  background: linear-gradient(90deg, #dc2626, #fb7185);
}

.project-grid-card__accent.is-info {
  background: linear-gradient(90deg, #2563eb, #38bdf8);
}

.project-grid-card__accent.is-neutral {
  background: linear-gradient(90deg, #64748b, #cbd5e1);
}

.project-grid-card__badge {
  position: absolute;
  top: -12px;
  left: 16px;
  z-index: 2;
  display: inline-flex;
  align-items: center;
  min-height: 34px;
  padding: 0 16px;
  border-radius: 999px;
  border: 1px solid rgba(191, 219, 254, 0.82);
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(243, 248, 255, 0.98));
  box-shadow:
    0 10px 24px rgba(15, 23, 42, 0.08),
    inset 0 1px 0 rgba(255, 255, 255, 0.96);
  color: #1d4ed8;
  font-size: 12px;
  font-weight: 900;
  white-space: nowrap;
  text-transform: lowercase;
}

.project-grid-card__badge--success {
  border-color: rgba(187, 247, 208, 0.92);
  color: #15803d;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(236, 253, 245, 0.98));
}

.project-grid-card__badge--warning {
  border-color: rgba(253, 224, 71, 0.34);
  color: #b45309;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(255, 251, 235, 0.98));
}

.project-grid-card__badge--danger {
  border-color: rgba(252, 165, 165, 0.72);
  color: #b91c1c;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(254, 242, 242, 0.98));
}

.project-grid-card__badge--info {
  border-color: rgba(191, 219, 254, 0.82);
  color: #1d4ed8;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(239, 246, 255, 0.98));
}

.project-grid-card__badge--neutral {
  border-color: rgba(226, 232, 240, 0.96);
  color: #475569;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(248, 250, 252, 0.98));
}

.project-grid-card__body {
  display: grid;
  gap: 12px;
}

.project-grid-card__title-col {
  display: grid;
  gap: 8px;
}

.project-grid-card__title-col strong {
  color: #3554b2;
  font-size: 18px;
  line-height: 1.16;
  font-weight: 900;
}

.project-grid-card__title-col span {
  display: -webkit-box;
  overflow: hidden;
  color: #475569;
  font-size: 13px;
  line-height: 1.5;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.project-grid-card__metrics {
  display: grid;
  gap: 10px;
}

.project-grid-card__progress-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: #3554b2;
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.project-grid-card__progress-track {
  height: 8px;
  overflow: hidden;
  margin-top: 8px;
  border-radius: 999px;
  background: #e2e8f0;
}

.project-grid-card__progress-track span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #2563eb, #60a5fa);
}

.project-grid-card__tasks-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.project-grid-card__tasks-row span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #475569;
  font-size: 12px;
  font-weight: 700;
}

.project-grid-card__risk {
  color: #b91c1c !important;
}

.project-grid-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: auto;
}

.project-grid-card__team-overlap {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.team-avatar {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border: 2px solid #ffffff;
  border-radius: 999px;
  color: #1d4ed8;
  background: #dbeafe;
  font-size: 11px;
  font-weight: 800;
}

.team-avatar--more {
  color: #475569;
  background: #e2e8f0;
}

.project-grid-card__actions {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.project-grid-card__actions button {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  border: 1px solid rgba(203, 213, 225, 0.95);
  border-radius: 12px;
  color: #475569;
  background: #ffffff;
  transition: transform 160ms ease, background 160ms ease, border-color 160ms ease, color 160ms ease;
}

.project-grid-card__actions button:hover {
  transform: translateY(-1px);
  color: #1d4ed8;
  border-color: rgba(191, 219, 254, 0.95);
  background: #eff6ff;
}

@media (max-width: 640px) {
  .project-grid-card {
    padding-top: 22px;
  }

  .project-grid-card__badge {
    left: 14px;
    min-height: 32px;
    padding-inline: 14px;
    font-size: 11px;
  }
}

.project-empty {
  grid-column: 1 / -1;
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
.project-grid-card {
  padding: 18px;
  border-radius: 22px;
  border-color: rgba(226, 232, 240, 0.96);
  box-shadow:
    0 14px 32px rgba(15, 23, 42, 0.06),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
}

.project-grid-card:hover,
.project-grid-card:focus-visible,
.project-grid-card.is-active {
  transform: translateY(-3px);
  border-color: rgba(251, 191, 36, 0.26);
  box-shadow:
    0 22px 42px rgba(15, 23, 42, 0.1),
    inset 0 1px 0 rgba(255, 255, 255, 0.92);
}

.project-grid-card__accent.is-success {
  background: linear-gradient(90deg, #10b981, #34d399);
}

.project-grid-card__accent.is-warning {
  background: linear-gradient(90deg, #f59e0b, #fbbf24);
}

.project-grid-card__accent.is-danger {
  background: linear-gradient(90deg, #ef4444, #fb7185);
}

.project-grid-card__accent.is-info {
  background: linear-gradient(90deg, #0ea5e9, #22d3ee);
}

.project-grid-card__accent.is-neutral {
  background: linear-gradient(90deg, #64748b, #cbd5e1);
}

.project-grid-card__title-col strong {
  font-weight: 900;
}

.project-grid-card__progress-track span {
  background: linear-gradient(90deg, #0f766e, #2dd4bf);
}

.team-avatar {
  color: #0f766e;
  background: #ccfbf1;
}

.project-grid-card__actions button:hover {
  color: #0f766e;
  border-color: rgba(45, 212, 191, 0.34);
  background: rgba(240, 253, 250, 1);
}

.project-empty {
  border-radius: 22px;
  background: rgba(255, 255, 255, 0.82);
}

.project-empty__icon {
  color: #0f766e;
  background: rgba(45, 212, 191, 0.12);
}
</style>
