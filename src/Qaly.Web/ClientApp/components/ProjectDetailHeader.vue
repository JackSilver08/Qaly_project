<script setup lang="ts">
import { ArrowLeft, LayoutDashboard } from 'lucide-vue-next'
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
  <header class="project-detail-header glass-card reveal">
    <div class="project-detail-header__top">
      <button class="back-link" type="button" @click="$emit('back')">
        <div class="back-icon">
          <ArrowLeft :size="18" />
        </div>
        <span>Quay lại danh sách</span>
      </button>
      
      <div class="project-detail-header__actions">
        <button class="assistant-button" type="button" @click="$emit('assistant')">
          <ChatbotAvatar size="launcher" />
          <span>Qaly AI Assistant</span>
        </button>
      </div>
    </div>

    <div class="project-detail-header__main">
      <div class="project-info">
        <div class="project-info__title-row">
          <div class="project-icon-box">
             <LayoutDashboard :size="24" class="text-primary" />
          </div>
          <div class="title-stack">
            <h1>{{ projectName }}</h1>
            <span :class="`project-status project-status--${statusTone}`">{{ statusLabel }}</span>
          </div>
        </div>
        <p v-if="description" class="project-info__desc">{{ description }}</p>
      </div>

      <div class="project-progress-summary">
        <div class="progress-info">
          <span>Tiến độ hoàn thành</span>
          <strong>{{ progressLabel }}</strong>
        </div>
        <div class="progress-bar-rail">
          <div class="progress-bar-fill" :style="{ width: `${progressPercentage}%` }">
             <div class="progress-shimmer"></div>
          </div>
        </div>
      </div>
    </div>
  </header>
</template>

<style scoped>
.project-detail-header {
  padding: 32px;
  margin-bottom: 32px;
  display: flex;
  flex-direction: column;
  gap: 28px;
  background:
    linear-gradient(160deg, rgba(255, 255, 255, 0.08), rgba(15, 76, 255, 0.1)),
    rgba(8, 21, 39, 0.74);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-shell);
  box-shadow: var(--shadow-card);
  backdrop-filter: blur(24px);
  position: relative;
  overflow: hidden;
}

.project-detail-header::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 4px;
  background: linear-gradient(90deg, var(--primary), var(--blue-600), var(--violet-500));
}

.project-detail-header__top {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.back-link {
  display: flex;
  align-items: center;
  gap: 12px;
  border: 0;
  background: transparent;
  color: var(--muted);
  font-weight: 700;
  padding: 0;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  cursor: pointer;
}

.back-icon {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(182, 194, 217, 0.26);
  border-radius: 10px;
  transition: all 0.3s;
}

.back-link:hover {
  color: var(--primary);
}

.back-link:hover .back-icon {
  border-color: rgba(117, 182, 255, 0.62);
  background: rgba(31, 128, 255, 0.22);
  transform: translateX(-4px);
}

.assistant-button {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 16px;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(182, 194, 217, 0.26);
  border-radius: 14px;
  font-weight: 800;
  color: var(--surface-milk);
  box-shadow: 0 10px 24px rgba(2, 8, 23, 0.32);
  transition: all 0.3s;
  cursor: pointer;
}

.assistant-button:hover {
  transform: translateY(-2px);
  border-color: rgba(117, 182, 255, 0.68);
  background: rgba(31, 128, 255, 0.2);
  box-shadow: 0 14px 28px rgba(15, 76, 255, 0.3);
}

.project-detail-header__main {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 40px;
  flex-wrap: wrap;
}

.project-info {
  flex: 1;
  min-width: 300px;
}

.project-info__title-row {
  display: flex;
  align-items: flex-start;
  gap: 20px;
  margin-bottom: 12px;
}

.project-icon-box {
  width: 56px;
  height: 56px;
  background: linear-gradient(135deg, #0f4cff, #22d3ee);
  display: grid;
  place-items: center;
  border-radius: 18px;
  flex-shrink: 0;
}

.project-icon-box :deep(svg) {
  color: #f8fafc;
}

.title-stack {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.project-info h1 {
  font-size: 32px;
  margin: 0;
  color: var(--surface-milk);
  font-weight: 800;
  line-height: 1.1;
  letter-spacing: -0.02em;
}

.project-info__desc {
  color: #b8c7de;
  font-size: 15px;
  line-height: 1.6;
  margin: 0;
  max-width: 700px;
  margin-left: 76px;
}

.project-progress-summary {
  width: 300px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.progress-info {
  display: flex;
  justify-content: space-between;
  font-size: 14px;
  font-weight: 600;
}

.progress-info span {
  color: var(--muted);
}

.progress-info strong {
  color: var(--surface-milk);
}

.progress-bar-rail {
  height: 12px;
  background: rgba(148, 163, 184, 0.24);
  border-radius: 6px;
  overflow: hidden;
  box-shadow: inset 0 2px 6px rgba(2, 8, 23, 0.52);
}

.progress-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #0f4cff, #22d3ee);
  border-radius: 6px;
  transition: width 1s cubic-bezier(0.65, 0, 0.35, 1);
  position: relative;
  overflow: hidden;
}

.progress-shimmer {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: linear-gradient(90deg, transparent, rgba(255,255,255,0.2), transparent);
  animation: shimmer 2s infinite linear;
}

@keyframes shimmer {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

.project-status {
  padding: 4px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  width: fit-content;
}

.project-status--active {
  border: 1px solid rgba(184, 219, 255, 0.44);
  background: linear-gradient(135deg, #0f4cff, #22d3ee);
  color: #f8fafc;
}

.project-status--planned {
  border: 1px solid rgba(196, 181, 253, 0.5);
  background: linear-gradient(135deg, #4f46e5, #8b5cf6);
  color: #f8fafc;
}

.project-status--archived {
  border: 1px solid rgba(182, 194, 217, 0.4);
  background: rgba(148, 163, 184, 0.24);
  color: #d2dff3;
}

.reveal {
  animation: fade-in-up 0.6s cubic-bezier(0.16, 1, 0.3, 1);
}

@keyframes fade-in-up {
  0% { opacity: 0; transform: translateY(20px); }
  100% { opacity: 1; transform: translateY(0); }
}
</style>
