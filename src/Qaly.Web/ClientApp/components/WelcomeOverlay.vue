<template>
  <div v-if="isVisible" class="welcome-overlay">
    <!-- Wipe panels -->
    <div class="wipe-panel left" :class="{ 'is-open': isOpen }"></div>
    <div class="wipe-panel right" :class="{ 'is-open': isOpen }"></div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';

// Initialize synchronously in setup to prevent the homepage from flashing
const cameFromLogin = typeof document !== 'undefined' && document.referrer.includes('/Account/Login');
const isNewLoginFlag = typeof window !== 'undefined' && window.localStorage && localStorage.getItem('qaly_new_login') === 'true';
const shouldShow = cameFromLogin || isNewLoginFlag;

const isVisible = ref(shouldShow);
const isOpen = ref(false); // Starts closed (covering the screen)

onMounted(() => {
  if (shouldShow) {
    localStorage.removeItem('qaly_new_login');
    
    // Slide open after 150ms once the app is ready
    setTimeout(() => {
      isOpen.value = true;
      
      // Once fully open, unmount the overlay completely
      setTimeout(() => {
        isVisible.value = false;
      }, 650);
    }, 150);
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
  pointer-events: none;
  overflow: hidden;
}

.wipe-panel {
  position: absolute;
  top: 0;
  height: 100%;
  width: 50.5%; /* Slightly over 50% to prevent sub-pixel seam gaps in the middle */
  background: linear-gradient(135deg, #050b18 0%, #0a1931 100%);
  transition: transform 0.65s cubic-bezier(0.85, 0, 0.15, 1);
  transform: scaleX(1); /* Starts closed (meeting in the center) */
  z-index: 9999;
  pointer-events: auto; /* Block user interactions during transition */
}

/* Left Panel */
.wipe-panel.left {
  left: 0;
  transform-origin: left;
  border-right: 2px solid #22d3ee;
  box-shadow: var(--qaly-shadow-md);
}

/* Right Panel */
.wipe-panel.right {
  right: 0;
  transform-origin: right;
  border-left: 2px solid #22d3ee;
  box-shadow: var(--qaly-shadow-md);
}

/* Open state (sliding back to sides) */
.wipe-panel.left.is-open {
  transform: scaleX(0);
}

.wipe-panel.right.is-open {
  transform: scaleX(0);
}
</style>
