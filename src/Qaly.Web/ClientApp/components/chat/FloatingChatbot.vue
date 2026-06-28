<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { X, Sparkles } from 'lucide-vue-next'
import ChatbotAvatar from '../ChatbotAvatar.vue'
import ErumiChatPanel from './ErumiChatPanel.vue'

const route = useRoute()
const isOpen = ref(false)

const isAnalyticsPage = computed(() => route.path === '/analytics')

function toggleDrawer() {
  isOpen.value = !isOpen.value
}
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
              <span class="drawer-title">Trợ lý Erumi</span>
              <span class="drawer-subtitle">Trợ lý AI phân tích</span>
            </div>
          </div>
          <button class="drawer-close-btn" @click="isOpen = false" aria-label="Đóng">
            <X :size="20" />
          </button>
        </header>
        
        <div class="drawer-body">
          <ErumiChatPanel :is-drawer="true" />
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
  padding: 16px 20px;
  border-bottom: 1px solid #f1f5f9;
  background: #ffffff;
}

.drawer-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.drawer-title-wrap {
  display: flex;
  flex-direction: column;
}

.drawer-title {
  font-weight: 700;
  color: #0f172a;
  font-size: 15px;
}

.drawer-subtitle {
  font-size: 11px;
  color: #64748b;
  font-weight: 500;
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
</style>
