<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ChatbotAvatar from '../ChatbotAvatar.vue'

const router = useRouter()
const route = useRoute()

const isAnalyticsPage = computed(() => route.path === '/analytics')

function navigateToAnalytics() {
  void router.push('/analytics')
}
</script>

<template>
  <div v-if="!isAnalyticsPage" class="floating-erumi">
    <!-- Floating Launcher to go to AI Analytics Page -->
    <button 
      class="erumi-launcher" 
      @click="navigateToAnalytics"
      aria-label="Đi đến Trợ lý Phân tích AI Erumi"
    >
      <ChatbotAvatar size="medium" />
      <!-- Online indicator -->
      <span class="launcher-badge"></span>
      <!-- Pulse ring -->
      <span class="pulse-ring"></span>
    </button>
    <!-- Tooltip -->
    <div class="erumi-tooltip">Hỏi Erumi AI</div>
  </div>
</template>

<style scoped>
.floating-erumi {
  position: fixed;
  bottom: 28px;
  right: 28px;
  z-index: 1000;
  font-family: 'Inter', sans-serif;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.erumi-launcher {
  width: 58px;
  height: 58px;
  border-radius: 18px;
  background: linear-gradient(145deg, #0f52ba 0%, #1e70e9 50%, #0a3d91 100%);
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  transition: all 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
  position: relative;
  cursor: pointer;
  box-shadow: 
    0 8px 24px rgba(15, 82, 186, 0.35),
    0 2px 8px rgba(15, 82, 186, 0.2),
    inset 0 1px 0 rgba(255,255,255,0.15);
}

.erumi-launcher:hover {
  transform: scale(1.08) translateY(-2px);
  box-shadow: 
    0 14px 32px rgba(15, 82, 186, 0.45),
    0 4px 12px rgba(15, 82, 186, 0.25),
    inset 0 1px 0 rgba(255,255,255,0.2);
  border-radius: 20px;
}

.erumi-launcher:hover ~ .erumi-tooltip {
  opacity: 1;
  transform: translateY(0);
  pointer-events: auto;
}

.launcher-badge {
  position: absolute;
  top: -2px;
  right: -2px;
  width: 13px;
  height: 13px;
  background: #22c55e;
  border: 2.5px solid #ffffff;
  border-radius: 50%;
}

.pulse-ring {
  position: absolute;
  top: -4px;
  right: -4px;
  width: 21px;
  height: 21px;
  border-radius: 50%;
  border: 2px solid #22c55e;
  animation: ping 2s cubic-bezier(0, 0, 0.2, 1) infinite;
  opacity: 0;
}

@keyframes ping {
  0% { transform: scale(0.8); opacity: 0.7; }
  70% { transform: scale(1.8); opacity: 0; }
  100% { transform: scale(1.8); opacity: 0; }
}

.erumi-tooltip {
  background: #0f172a;
  color: #ffffff;
  font-size: 12px;
  font-weight: 600;
  padding: 6px 12px;
  border-radius: 8px;
  white-space: nowrap;
  opacity: 0;
  transform: translateY(4px);
  transition: all 0.2s ease;
  pointer-events: none;
  box-shadow: 0 4px 12px rgba(0,0,0,0.15);
}

.floating-erumi:hover .erumi-tooltip {
  opacity: 1;
  transform: translateY(0);
}
</style>
