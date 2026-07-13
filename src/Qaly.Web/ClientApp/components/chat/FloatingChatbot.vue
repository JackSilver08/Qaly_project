<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Activity, MessageSquare, X, Sparkles } from 'lucide-vue-next'
import ChatbotAvatar from '../ChatbotAvatar.vue'
import ErumiChatPanel from './ErumiChatPanel.vue'
import AiActivityPanel from './AiActivityPanel.vue'

const route = useRoute()
const router = useRouter()
const isOpen = ref(false)
const activeView = ref<'chat' | 'activity'>('chat')

const isAnalyticsPage = computed(() => route.path === '/analytics')

function toggleDrawer() {
  if (isOpen.value) {
    closeDrawer()
    return
  }

  isOpen.value = true
}

function closeDrawer() {
  isOpen.value = false

  if (!route.query.aiActivity && !route.query.aiJob && !route.query.aiDraft) return

  const query = { ...route.query }
  delete query.aiActivity
  delete query.aiTab
  delete query.aiJob
  delete query.aiDraft
  void router.replace({ query })
}

watch(
  () => [route.query.aiActivity, route.query.aiJob, route.query.aiDraft],
  ([activity, jobId, draftId]) => {
    if (activity === '1' || jobId || draftId) {
      isOpen.value = true
      activeView.value = 'activity'
    }
  },
  { immediate: true },
)
</script>

<template>
  <div v-if="!isAnalyticsPage" class="global-erumi-chatbot-widget">
    <!-- Floating Bubble Trigger -->
    <button 
      class="erumi-bubble-trigger" 
      :class="{ 'is-active': isOpen }" 
      aria-label="Erumi Assistant"
      @click="toggleDrawer"
    >
      <ChatbotAvatar size="medium" />
      <span class="bubble-sparkle">
        <Sparkles :size="14" />
      </span>
    </button>

    <!-- Side Drawer Overlay -->
    <Transition name="slide-drawer">
      <div v-if="isOpen" class="erumi-side-drawer">
        <header class="drawer-header">
          <div class="drawer-header-left">
            <ChatbotAvatar size="small" />
            <div class="drawer-title-wrap">
              <span class="drawer-title">{{ activeView === 'chat' ? 'Trợ lý Erumi' : 'Hoạt động AI' }}</span>
              <span class="drawer-subtitle">{{ activeView === 'chat' ? 'Trợ lý AI phân tích' : 'Jobs và bản nháp cần duyệt' }}</span>
            </div>
          </div>
          <div class="drawer-header-actions">
            <button class="drawer-icon-btn" :class="{ active: activeView === 'chat' }" title="Trò chuyện" @click="activeView = 'chat'"><MessageSquare :size="17" /></button>
            <button class="drawer-icon-btn" :class="{ active: activeView === 'activity' }" title="Hoạt động AI" @click="activeView = 'activity'"><Activity :size="17" /></button>
            <button class="drawer-close-btn" @click="closeDrawer" aria-label="Đóng"><X :size="20" /></button>
          </div>
        </header>
        
        <div class="drawer-body">
          <ErumiChatPanel v-if="activeView === 'chat'" :is-drawer="true" />
          <AiActivityPanel v-else />
        </div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.global-erumi-chatbot-widget {
  position: relative;
  z-index: 9999;
}

/* Floating Bubble Button */
.erumi-bubble-trigger {
  position: fixed;
  bottom: 24px;
  right: 24px;
  width: 56px;
  height: 56px;
  border-radius: 50%;
  background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
  border: none;
  box-shadow: var(--qaly-shadow-md);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.erumi-bubble-trigger:hover {
  transform: translateY(-4px) scale(1.06);
  box-shadow: var(--qaly-shadow-md);
}

.erumi-bubble-trigger.is-active {
  transform: rotate(90deg) scale(0.9);
  background: #64748b;
  box-shadow: var(--qaly-shadow-md);
}

.bubble-sparkle {
  position: absolute;
  top: -2px;
  right: -2px;
  background: #f59e0b;
  color: white;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 6px rgba(245, 158, 11, 0.4);
}

/* Side Drawer Panel */
.erumi-side-drawer {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 380px;
  background: #ffffff;
  box-shadow: var(--qaly-shadow-md);
  display: flex;
  flex-direction: column;
  border-left: 1px solid #e2e8f0;
}

.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 58px;
  padding: 10px 14px;
  border-bottom: 1px solid #f1f5f9;
  background: #ffffff;
}

.drawer-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.drawer-header-left :deep(.chatbot-avatar--small) {
  width: 34px;
  height: 34px;
}

.drawer-title-wrap {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.drawer-title {
  font-weight: 700;
  color: #0f172a;
  font-size: 15px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.drawer-subtitle {
  font-size: 11px;
  color: #64748b;
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.drawer-close-btn {
  background: transparent;
  border: none;
  color: #64748b;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  transition: all 0.2s ease;
}

.drawer-close-btn:hover {
  background: #f1f5f9;
  color: #0f172a;
}

.drawer-header-actions {
  display: flex;
  align-items: center;
  gap: 2px;
  flex: 0 0 auto;
}

.drawer-icon-btn {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border: 1px solid transparent;
  background: transparent;
  color: #64748b;
  cursor: pointer;
}

.drawer-icon-btn:hover {
  background: #f1f5f9;
  color: #0f172a;
}

.drawer-icon-btn.active {
  border-color: #b9d8cc;
  background: #eaf6f0;
  color: #24735b;
}

.drawer-body {
  flex: 1;
  overflow: hidden;
  height: 100%;
}

/* Drawer slide transition */
.slide-drawer-enter-active,
.slide-drawer-leave-active {
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.slide-drawer-enter-from,
.slide-drawer-leave-to {
  transform: translateX(100%);
}

@media (max-width: 480px) {
  .erumi-side-drawer {
    width: 100vw;
  }

  .drawer-header {
    padding-inline: 10px;
  }

  .drawer-header-left {
    gap: 8px;
  }
}
</style>
