<script setup lang="ts">
import { ArrowLeft } from 'lucide-vue-next'
import ChatbotAvatar from './ChatbotAvatar.vue'

defineProps<{
  projectName: string
  description: string | null
  statusLabel: string
  statusTone: string
  progressLabel: string
  progressPercentage: number
}>()

defineEmits<{
  back: []
  assistant: []
}>()
</script>

<template>
  <header class="project-detail-header glass-card">
    <div class="project-detail-header__top">
      <button class="back-link" type="button" @click="$emit('back')">
        <ArrowLeft :size="18" />
        <span>Quay lại danh sách</span>
      </button>
      
      <div class="project-detail-header__actions">
        <button class="secondary-button" type="button" @click="$emit('assistant')">
          <ChatbotAvatar size="launcher" />
          <span>Qaly Assistant</span>
        </button>
      </div>
    </div>

    <div class="project-detail-header__main">
      <div class="project-info">
        <div class="project-info__title-row">
          <h1>{{ projectName }}</h1>
          <span :class="`project-status project-status--${statusTone}`">{{ statusLabel }}</span>
        </div>
        <p v-if="description" class="project-info__desc">{{ description }}</p>
      </div>

      <div class="project-progress-summary">
        <div class="progress-info">
          <span>Tiến độ tổng quan</span>
          <strong>{{ progressLabel }}</strong>
        </div>
        <div class="progress-bar-rail">
          <div class="progress-bar-fill" :style="{ width: `${progressPercentage}%` }"></div>
        </div>
      </div>
    </div>
  </header>
</template>

<style scoped>
.project-detail-header {
  padding: 24px;
  margin-bottom: 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
  background: var(--surface-milk);
  border: 1px solid var(--glass-border);
}

.project-detail-header__top {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.back-link {
  display: flex;
  align-items: center;
  gap: 8px;
  border: 0;
  background: transparent;
  color: var(--muted);
  font-weight: 600;
  padding: 0;
  transition: color 0.2s;
  cursor: pointer;
}

.back-link:hover {
  color: var(--primary);
}

.project-detail-header__main {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 32px;
  flex-wrap: wrap;
}

.project-info {
  flex: 1;
  min-width: 300px;
}

.project-info__title-row {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.project-info h1 {
  font-size: 24px;
  margin: 0;
  color: var(--text);
  font-weight: 800;
}

.project-info__desc {
  color: var(--muted);
  font-size: 14px;
  margin: 0;
  max-width: 600px;
}

.project-progress-summary {
  width: 240px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.progress-info {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
}

.progress-info span {
  color: var(--muted);
}

.progress-info strong {
  color: var(--text);
  font-weight: 700;
}

.progress-bar-rail {
  height: 8px;
  background: var(--line);
  border-radius: 4px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  background: var(--primary);
  border-radius: 4px;
  transition: width 0.4s ease;
}

.project-status {
  padding: 4px 10px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 700;
}

.project-status--active {
  background: var(--primary-soft);
  color: var(--primary);
}

.project-status--planned {
  background: var(--violet-100);
  color: var(--violet-500);
}

.project-status--archived {
  background: var(--line);
  color: var(--muted);
}
</style>
