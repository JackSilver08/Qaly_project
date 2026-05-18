<template>
  <Transition name="welcome-fade">
    <div v-if="isVisible" class="welcome-overlay" :class="{ 'is-leaving': isLeaving }">
      <div class="welcome-content">
        <div class="robot-container">
          <img :src="'/dist/images/qaly-bot-final-ultra.png'" alt="Welcome Robot" class="robot-img" />
          <div class="speech-bubble">
            <span class="hello-text">Xin chào!</span>
            <span class="welcome-text">Chào mừng bạn quay trở lại Qaly Workspace.</span>
          </div>
        </div>
      </div>
      
      <!-- Wipe panels -->
      <div class="wipe-panel left"></div>
      <div class="wipe-panel right"></div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';

const isVisible = ref(false);
const isLeaving = ref(false);

onMounted(() => {
  // Trigger animation if the user just came from the Login page
  const cameFromLogin = document.referrer.includes('/Account/Login');
  const isNewLoginFlag = localStorage.getItem('qaly_new_login') === 'true';
  
  if (cameFromLogin || isNewLoginFlag) {
    isVisible.value = true;
    
    // Clear the flag just in case
    localStorage.removeItem('qaly_new_login');
    
    // Start leaving sequence after 2.5 seconds
    setTimeout(() => {
      isLeaving.value = true;
      
      // Fully hide after animation
      setTimeout(() => {
        isVisible.value = false;
      }, 1000);
    }, 2500);
  }
});
</script>

<style scoped>
.welcome-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  background:
    radial-gradient(circle at 82% 8%, rgba(31, 128, 255, 0.28), transparent 35%),
    linear-gradient(145deg, #050b18 0%, #081527 46%, #0b1e3a 100%);
  overflow: hidden;
}

.welcome-content {
  position: relative;
  z-index: 10;
  text-align: center;
  transition: opacity 0.5s ease;
}

.is-leaving .welcome-content {
  opacity: 0;
}

.robot-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 24px;
}

.robot-img {
  width: 140px;
  height: 140px;
  object-fit: contain;
  margin-bottom: 15px;
  filter: drop-shadow(0 14px 28px rgba(15, 76, 255, 0.4));
  animation: robot-float 3s ease-in-out infinite;
}

.speech-bubble {
  background: rgba(255, 255, 255, 0.09);
  border: 1px solid rgba(182, 194, 217, 0.24);
  padding: 20px 32px;
  border-radius: 24px;
  position: relative;
  box-shadow: 0 16px 34px rgba(2, 8, 23, 0.42);
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-width: 300px;
}

.speech-bubble::after {
  content: '';
  position: absolute;
  top: -12px;
  left: 50%;
  transform: translateX(-50%);
  border-left: 12px solid transparent;
  border-right: 12px solid transparent;
  border-bottom: 12px solid rgba(255, 255, 255, 0.09);
}

.hello-text {
  font-size: 24px;
  font-weight: 800;
  color: #22d3ee;
  display: block;
}

.welcome-text {
  font-size: 15px;
  color: #d2def3;
  font-weight: 500;
}

/* Wipe Panels */
.wipe-panel {
  position: absolute;
  top: 0;
  height: 100%;
  width: 50%;
  background: linear-gradient(145deg, #0f4cff, #1f80ff);
  transition: transform 0.8s cubic-bezier(0.77, 0, 0.175, 1);
  transform: scaleX(0);
  z-index: 5;
}

.wipe-panel.left {
  left: 0;
  transform-origin: left;
}

.wipe-panel.right {
  right: 0;
  transform-origin: right;
}

.is-leaving .wipe-panel {
  transform: scaleX(1);
}

/* Animations */
@keyframes robot-float {
  0%, 100% { transform: translateY(0) rotate(0deg); }
  25% { transform: translateY(-15px) rotate(5deg); }
  75% { transform: translateY(-5px) rotate(-5deg); }
}

/* Vue Transitions */
.welcome-fade-leave-active {
  transition: opacity 1s ease;
}

.welcome-fade-leave-to {
  opacity: 0;
}
</style>
