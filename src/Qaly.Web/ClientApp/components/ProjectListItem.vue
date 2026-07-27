<script setup lang="ts">
import { CalendarDays, CheckCircle2, Eye, Pencil, Trash2, UserRound } from 'lucide-vue-next'
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
      <div class="project-list-item__header">
        <div class="project-list-item__title">
          <strong>{{ project.name }}</strong>
          <span :class="`project-status project-status--${project.statusTone}`">
            {{ project.statusLabel }}
          </span>
        </div>

        <span class="project-list-item__progress-label">{{ project.progressPercentage }}%</span>
      </div>

      <p>{{ project.description }}</p>

      <div class="project-list-item__meta">
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
        <span v-if="project.overdueTaskCount > 0" class="project-risk">
          {{ project.overdueTaskCount }} quá hạn
        </span>
      </div>

      <div class="project-list-item__progress" aria-hidden="true">
        <span :style="{ width: `${project.progressPercentage}%` }" />
      </div>
    </button>

    <div class="project-list-item__side">
      <div class="project-list-item__team" aria-label="Thành viên dự án">
        <span v-for="(member, index) in project.memberInitials" :key="`${member}-${index}`">
          {{ member }}
        </span>
      </div>

      <div class="project-list-item__actions">
        <button type="button" aria-label="Xem dự án" @click="$emit('view', project.id)">
          <Eye :size="16" />
        </button>
        <button v-if="!readOnly" type="button" aria-label="Sửa dự án" @click="$emit('edit', project.id)">
          <Pencil :size="16" />
        </button>
        <button v-if="!readOnly" type="button" aria-label="Xóa dự án" @click="$emit('delete', project.id)">
          <Trash2 :size="16" />
        </button>
      </div>
    </div>
  </article>
</template>

<style scoped>
.project-list-item {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 14px;
  padding: 16px;
  border: 1px solid rgba(226, 232, 240, 0.95);
  border-radius: 18px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(248, 250, 252, 0.96));
  box-shadow:
    0 8px 18px rgba(15, 23, 42, 0.04),
    inset 0 1px 0 rgba(255, 255, 255, 0.88);
  transition:
    transform 180ms ease,
    box-shadow 180ms ease,
    border-color 180ms ease;
}

.project-list-item:hover,
.project-list-item:focus-within,
.project-list-item.is-active {
  transform: translateY(-1px);
  border-color: rgba(191, 219, 254, 0.95);
  box-shadow:
    0 16px 34px rgba(15, 23, 42, 0.08),
    inset 0 1px 0 rgba(255, 255, 255, 0.92);
}

.project-list-item__main {
  min-width: 0;
  display: grid;
  gap: 12px;
  padding: 0;
  border: 0;
  background: transparent;
  text-align: left;
}

.project-list-item__header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.project-list-item__title {
  min-width: 0;
  display: grid;
  gap: 8px;
}

.project-list-item__title strong {
  overflow: hidden;
  color: #0f172a;
  font-size: 16px;
  font-weight: 800;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-status {
  width: fit-content;
  display: inline-flex;
  align-items: center;
  min-height: 26px;
  padding: 0 10px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
}

.project-status--success {
  color: #166534;
  background: #dcfce7;
}

.project-status--warning {
  color: #92400e;
  background: #fef3c7;
}

.project-status--danger {
  color: #991b1b;
  background: #fee2e2;
}

.project-status--info {
  color: #1d4ed8;
  background: #dbeafe;
}

.project-status--neutral {
  color: #475569;
  background: #e2e8f0;
}

.project-list-item__progress-label {
  color: #1d4ed8;
  font-size: 13px;
  font-weight: 900;
}

.project-list-item__main p {
  overflow: hidden;
  color: #64748b;
  line-height: 1.55;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.project-list-item__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 10px 16px;
}

.project-list-item__meta span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #475569;
  font-size: 12px;
  font-weight: 700;
}

.project-risk {
  color: #b91c1c !important;
}

.project-list-item__progress {
  overflow: hidden;
  height: 8px;
  border-radius: 999px;
  background: #e2e8f0;
}

.project-list-item__progress span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #2563eb, #38bdf8);
}

.project-list-item__side {
  display: grid;
  align-content: space-between;
  justify-items: end;
  gap: 12px;
  min-width: 124px;
}

.project-list-item__team {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 6px;
}

.project-list-item__team span {
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

.project-list-item__actions {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.project-list-item__actions button {
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

.project-list-item__actions button:hover {
  transform: translateY(-1px);
  color: #1d4ed8;
  border-color: rgba(191, 219, 254, 0.95);
  background: #eff6ff;
}

@media (max-width: 760px) {
  .project-list-item {
    grid-template-columns: 1fr;
  }

  .project-list-item__side {
    min-width: 0;
    justify-items: start;
  }

  .project-list-item__team {
    justify-content: flex-start;
  }
}
</style>

<style scoped>
.project-list-item {
  padding: 18px;
  border-radius: 22px;
  border-color: rgba(226, 232, 240, 0.95);
  box-shadow:
    0 14px 32px rgba(15, 23, 42, 0.06),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
}

.project-list-item:hover,
.project-list-item:focus-within,
.project-list-item.is-active {
  transform: translateY(-2px);
  border-color: rgba(45, 212, 191, 0.24);
  box-shadow:
    0 20px 42px rgba(15, 23, 42, 0.1),
    inset 0 1px 0 rgba(255, 255, 255, 0.92);
}

.project-list-item__title strong {
  font-weight: 900;
}

.project-status--info {
  color: #0f766e;
  background: #ccfbf1;
}

.project-list-item__progress-label {
  color: #0f766e;
}

.project-list-item__progress span {
  background: linear-gradient(90deg, #0f766e, #2dd4bf);
}

.project-list-item__team span {
  color: #0f766e;
  background: #ccfbf1;
}

.project-list-item__actions button:hover {
  color: #0f766e;
  border-color: rgba(45, 212, 191, 0.34);
  background: rgba(240, 253, 250, 1);
}
</style>
